//
// fileops.cs: file operations
//
// Author:
//   Miguel de Icaza (miguel@gnome.org)
//
// Licensed under the MIT X11 license
//
using System;
using System.IO;
using System.Collections.Generic;
using Mono.Unix;
using Mono.Unix.Native;
using System.Runtime.InteropServices;

namespace MouselessCommander {
	public class FileOperation : IDisposable {
		protected Progress Progress;
		protected IntPtr io_buffer;
		protected const int COPY_BUFFER_SIZE = 64 * 1024;

		public FileOperation (Progress progress)
		{
			Progress = progress;
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
			
		public CopyOperation (Progress progress) : base (progress)
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
					switch (Error.Query (Error.Result.RetryIgnoreCancel, msg, "While creating \"{0}\"", target_path)){
					case Error.Result.Retry:
						continue;
					case Error.Result.Ignore:
						break;
					case Error.Result.Cancel:
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

		public bool CopyFile (string source_absolute_path, string target_path)
		{
			// Open Source
			int source_fd;
			while (true){
				source_fd = Syscall.open (source_absolute_path, OpenFlags.O_RDONLY, (FilePermissions) 0);
				if (source_fd != -1)
					break;
				Errno errno = Stdlib.GetLastError ();
				if (errno == Errno.EINTR)
					continue;
			
				var msg = UnixMarshal.GetErrorDescription  (errno);
				switch (Error.Query (Error.Result.RetryCancel, msg, "While opening \"{0}\"", target_path)){
				case Error.Result.Retry:
					continue;
				case Error.Result.Cancel:
					return false;
				}
			
			}

			bool ret = false;
			// Open target
			int target_fd;
			while (true){
				target_fd = Syscall.open (target_path, OpenFlags.O_WRONLY, FilePermissions.S_IWUSR);
				if (target_fd != -1)
					break;
				Errno errno = Stdlib.GetLastError ();
				if (errno == Errno.EINTR)
					continue;
			
				var msg = UnixMarshal.GetErrorDescription  (errno);
				switch (Error.Query (Error.Result.RetryCancel, msg, "While creating \"{0}\"", source_absolute_path)){
				case Error.Result.Retry:
					continue;
				case Error.Result.Cancel:
					goto close_source;
				}
			}

			AllocateBuffer ();
			long n;
			
			while (true){
				n = Syscall.read (source_fd, io_buffer, COPY_BUFFER_SIZE);

				if (n != -1)
					break;

				Errno errno = Stdlib.GetLastError ();
				if (errno == Errno.EINTR)
					continue;
			
				var msg = UnixMarshal.GetErrorDescription  (errno);
				switch (Error.Query (Error.Result.RetryCancel, msg, "While reading \"{0}\"", source_absolute_path)){
				case Error.Result.Retry:
					continue;
				case Error.Result.Cancel:
					goto close_both;
				}
			}
			while (true){
				long count = Syscall.write (target_fd, io_buffer, (ulong) n);
				if (count != -1)
					break;
			}
			ret = true;
			
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