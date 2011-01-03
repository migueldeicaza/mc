//
// fileops.cs: file operations
//
// Author:
//   Miguel de Icaza (miguel@gnome.org)
//
// Licensed under the MIT X11 license
//
// This file does not contain any UI code, it depends on the interfaces
// for it, that way we can later implement the background operations
// and notifications, or use without MouselessCommander.
//
using System;
using System.IO;
using System.Collections.Generic;
using Mono.Unix;
using Mono.Unix.Native;
using System.Runtime.InteropServices;

namespace MouselessCommander {
	[Flags]
	public enum OResult {
		Retry = 1, Ignore = 2, Cancel = 4,
		RetryCancel = 5,
		RetryIgnoreCancel = 7
	}
	
	public interface IUserInteraction {
		OResult Query (OResult flags, string errormsg, string condition, string file);
	}

	public interface IProgressInteraction : IUserInteraction {
		void Step ();
	}
	
	public class FileOperation : IDisposable {
		protected IProgressInteraction Interaction;
		protected IntPtr io_buffer;
		protected const int COPY_BUFFER_SIZE = 64 * 1024;

		public FileOperation (IProgressInteraction interaction)
		{
			Interaction = interaction;
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (true);
							  
		}

		protected virtual void Dispose (bool disposing)
		{
			if (disposing){
				// release managed
			}
			
			if (io_buffer != IntPtr.Zero){
				Marshal.FreeHGlobal (io_buffer);
				io_buffer = IntPtr.Zero;
			}
		}

		protected void AllocateBuffer ()
		{
			if (io_buffer == IntPtr.Zero)
				io_buffer = Marshal.AllocHGlobal (COPY_BUFFER_SIZE);
		}
	}
	
	public class CopyOperation : FileOperation {
		Dictionary<string,FilePermissions> dirs_created;
			
		public CopyOperation (IProgressInteraction interaction) : base (interaction)
		{
			dirs_created = new Dictionary<string,FilePermissions> ();
		}

		bool CopyDirectory (string source_absolute_path, string target_path, FilePermissions protection)
		{
			if (!dirs_created.ContainsKey (target_path)){
				while (true){
					int r = Syscall.mkdir (target_path, protection | FilePermissions.S_IRWXU);
					if (r != -1)
						break;
					
					Errno errno = Stdlib.GetLastError ();
					if (errno == Errno.EINTR)
						continue;
					
					if (errno == Errno.EEXIST || errno == Errno.EISDIR)
						break;
					
					var msg = UnixMarshal.GetErrorDescription  (errno);
					switch (Interaction.Query (OResult.RetryIgnoreCancel, msg, "While creating \"{0}\"", target_path)){
					case OResult.Retry:
						continue;
					case OResult.Ignore:
						break;
					case OResult.Cancel:
						return false;
					}
				} 
				dirs_created [target_path] = protection;
			}
			
			var udi = new UnixDirectoryInfo (source_absolute_path);
			foreach (var entry in udi.GetFileSystemEntries ()){
				if (entry.Name == "." || entry.Name == "..")
					continue;

				string source = Path.Combine (source_absolute_path, entry.Name);
				string target = Path.Combine (target_path, entry.Name);
				if (entry.IsDirectory)
					if (!CopyDirectory (source, target, entry.Protection))
						return false;
				else
					if (!CopyFile (source, target))
						return false;
			}
			return true;
		}

		bool ShouldRetryOperation (string text, string file)
		{
			Errno errno = Stdlib.GetLastError ();
			if (errno == Errno.EINTR)
				return true;
			var msg = UnixMarshal.GetErrorDescription  (errno);
			if (Interaction.Query (OResult.RetryCancel, msg, text, file) == OResult.Retry)
				return true;
			return false;
		}
		
		public bool CopyFile (string source_absolute_path, string target_path)
		{
			bool ret = false;
			
			// Open Source
			int source_fd;
			while (true){
				source_fd = Syscall.open (source_absolute_path, OpenFlags.O_RDONLY, (FilePermissions) 0);
				if (source_fd != -1)
					break;

				if (ShouldRetryOperation ("While opening \"{0}\"", target_path))
					continue;
				return false;
			}
			Stat stat;
			while (true){
				if (Syscall.fstat (source_fd, out stat) != -1)
					break;

				if (ShouldRetryOperation ("While probing for state of \"{0}\"", target_path))
					continue;
				goto close_source;
			}
			
			// Open target
			int target_fd;
			while (true){
				target_fd = Syscall.open (target_path, OpenFlags.O_WRONLY, FilePermissions.S_IWUSR);
				if (target_fd != -1)
					break;
				if (ShouldRetryOperation ("While creating \"{0}\"", target_path))
					continue;
				goto close_source;
			}

			AllocateBuffer ();
			long n;
			
			while (true){
				n = Syscall.read (source_fd, io_buffer, COPY_BUFFER_SIZE);

				if (n != -1)
					break;

				if (ShouldRetryOperation ("While reading \"{0}\"", source_absolute_path))
					continue;
				goto close_both;
			}
			while (true){
				long count = Syscall.write (target_fd, io_buffer, (ulong) n);
				if (count != -1)
					break;

				if (ShouldRetryOperation ("While writing \"{0}\"", target_path))
					continue;
				goto close_both;
			}

			// File mode
			while (true){
				n = Syscall.fchmod (target_fd, stat.st_mode);
				if (n == 0)
					break;

				if (ShouldRetryOperation ("Setting permissions on \"{0}\"", target_path))
					continue;

				goto close_both;
			}

			// The following are not considered errors if we can not set them
			ret = true;
			
			// preserve owner and group if running as root
			if (Syscall.geteuid () == 0)
				Syscall.fchown (target_fd, stat.st_uid, stat.st_gid);
			
			// Set file time
			Timeval [] dates = new Timeval [2] {
				new Timeval () { tv_sec = stat.st_atime },
				new Timeval () { tv_sec = stat.st_mtime }
			};
			Syscall.futimes (target_fd, dates);
			
		close_both:
			Syscall.close (target_fd);
		close_source:
			Syscall.close (source_fd);
			return ret;
		}
		
		public bool Perform (string cwd, string source_path, bool is_dir, FilePermissions protection, string target)
		{
			string source_absolute_path = Path.Combine (cwd, source_path);
			string target_path = Path.Combine (target, source_path);

			if (is_dir)
				if (!CopyDirectory (source_absolute_path, target_path, protection))
					return false;
			else
				if (!CopyFile (source_absolute_path, target_path))
					return false;

			return true;
		}

		
	}
}