//
// mc.cs: Panel controls
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Licensed under the MIT X11 license
//
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using Mono.Terminal;
using Mono.Unix;
using Mono.Unix.Native;
using System.Runtime.InteropServices;

namespace MouselessCommander {

	public class Listing : IEnumerable<Listing.FileNode> {
		string url;
		FileNode [] nodes;
		Comparison<FileNode> compare;
		
		public int Count { get; private set; }
		
		public class FileNode {
			public int StartIdx;
			public int Nested;
			public bool Marked;
			public UnixFileSystemInfo Info;

			//
			// Have to use this ugly hack, because Mono.Posix does not
			// return ".." in directory listings.   And creating instances
			// of it, return the parent name, instead of ".."
			//
			public virtual string Name {
				get {
					return Info.Name;
				}
			}
			
			public FileNode (UnixFileSystemInfo info)
			{
				Info = info;
			}
		}

		public class DirNode : FileNode {
			public FileNode [] Nodes;
			public bool Expanded;
			
			public DirNode (UnixFileSystemInfo info) : base (info) {}
		}

		public class DirNodeDotDot : DirNode {
			public DirNodeDotDot (UnixFileSystemInfo info) : base (info) {}
			
			public override string Name {
				get {
					return "..";
				}
			}
		}
		
		public IEnumerator<FileNode> GetEnumerator ()
		{
			foreach (var a in nodes)
				yield return a;
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			foreach (var a in nodes)
				yield return a;
		}

		FileNode GetNodeAt (int idx, FileNode [] nodes)
		{
			if (nodes == null)
				throw new ArgumentNullException ("nodes");

			for (int i = 0; i < nodes.Length; i++){
				if (nodes [i].StartIdx == idx)
					return nodes [i];
				if (i+1 == nodes.Length || nodes [i+1].StartIdx > idx)
					return GetNodeAt (idx, ((DirNode) nodes [i]).Nodes);
			}
			return null;
		}

		string GetName (int idx, string start, FileNode [] nodes)
		{
			for (int i = 0; i < nodes.Length; i++){
				if (nodes [i].StartIdx == idx)
					return Path.Combine (start, nodes [i].Name);
				if (i+1 == nodes.Length || nodes [i+1].StartIdx > idx)
					return GetName (idx, Path.Combine (start, nodes [i].Name), ((DirNode) nodes [i]).Nodes);
			}
			throw new Exception ("This should not happen");
		}

		string GetPathAt (int idx, FileNode [] nodes)
		{
			for (int i = 0; i < nodes.Length; i++){
				if (nodes [i].StartIdx == idx)
					return nodes [i].Name;
				if (i+1 == nodes.Length || nodes [i+1].StartIdx > idx)
					return Path.Combine (nodes [i].Name, GetPathAt (idx, ((DirNode) nodes [i]).Nodes));
			}
			return null;
		}
		
		public string GetPathAt (int idx)
		{
			return GetPathAt (idx, nodes);
		}

		public int NodeWithName (string s)
		{
			for (int i = 0; i < nodes.Length; i++){
				if (nodes [i].Name == s)
					return i;
			}
			return -1;
			
		}
		
		public FileNode this [int idx]{
			get {
				var x = GetNodeAt (idx, nodes);
				return x;
			}
		}

		FileNode [] PopulateNodes (bool need_dotdot, UnixFileSystemInfo [] root)
		{
			FileNode [] pnodes = new FileNode [root.Length + (need_dotdot ? 1 : 0)];
			int i = 0;

			if (need_dotdot){
				pnodes [0] = new DirNodeDotDot (new UnixDirectoryInfo (".."));
				i++;
			}
			
			foreach (var info in root){
				if (info.IsDirectory)
					pnodes [i++] = new DirNode (info);
				else
					pnodes [i++] = new FileNode (info);
			}

			Array.Sort<FileNode> (pnodes, compare);
			return pnodes;
		}

		int UpdateIndexes (FileNode [] nodes, int start, int level)
		{
			foreach (var n in nodes){
				DirNode dn = n as DirNode;
				n.StartIdx = start++;
				n.Nested = level;
				if (dn != null && dn.Expanded && dn.Nodes != null)
					start = UpdateIndexes (dn.Nodes, start, level+1);
			}
			return start;
		}
		
		Listing (string url, UnixFileSystemInfo [] root, Comparison<FileNode> compare)
		{
			this.url = url;
			this.compare = compare;
			nodes = PopulateNodes (url != "/", root);
			Count = UpdateIndexes (nodes, 0, 0);
		}

		public Listing ()
		{
			nodes = new FileNode [0];
		}

		void LoadChild (int idx)
		{
			DirNode dn = this [idx] as DirNode;
			string name = GetName (idx, url, nodes);
				
			try {
				var udi = new UnixDirectoryInfo (name);
				dn.Nodes = PopulateNodes (true, udi.GetFileSystemEntries ());
				dn.Expanded = true;
			} catch (Exception e){
				Console.WriteLine ("Error loading {0}", name);
				// Show error?
				return;
			}
			Count = UpdateIndexes (nodes, 0, 0);
		}

		public void Expand (int idx)
		{
			var node = this [idx] as DirNode;
			if (node == null)
				return;
			if (node.Nodes == null){
				LoadChild (idx);
			} else {
				node.Expanded = true;
				Count = UpdateIndexes (nodes, 0, 0);
			}
		}

		public void Collapse (int idx)
		{
			var node = this [idx] as DirNode;
			if (node == null)
				return;
			node.Expanded = false;
			foreach (var n in node.Nodes)
				n.Marked = false;
			
			Count = UpdateIndexes (nodes, 0, 0);
		}
		
		public static Listing LoadFrom (string url, Comparison<FileNode> compare)
		{
			try {
				var udi = new UnixDirectoryInfo (url);
				return new Listing (url, udi.GetFileSystemEntries (), compare);
			} catch (Exception e){
				return new Listing ();
			}
			return null;
		}
	}
	
	public class Panel : Frame {
		static int ColorDir;
		Shell shell;
		
		SortOrder sort_order = SortOrder.Name;
		bool group_dirs = true;
		Listing listing;
		int top, selected, marked;
		string current_path;
		
		static Panel ()
		{
			if (Application.UsingColor)
				ColorDir = Curses.A_BOLD | Application.MakeColor (Curses.COLOR_WHITE, Curses.COLOR_BLUE);
			else
				ColorDir = Curses.A_NORMAL;
		}
		
		public enum SortOrder {
			Unsorted,
			Name,
			Extension,
			ModifyTime,
			AccessTime,
			ChangeTime,
			Size,
			Inode
		}
		
		int CompareNodes (Listing.FileNode a, Listing.FileNode b)
		{
			if (a.Name == ".."){
				if (b.Name != "..")
					return -1;
				return 0;
			}
		
			int nl = a.Nested - b.Nested;
			if (nl != 0)
				return nl;

			if (sort_order == SortOrder.Unsorted)
				return 0;
			
			if (group_dirs){
				bool adir = a is Listing.DirNode;
				bool bdir = b is Listing.DirNode;

				if (adir ^ bdir){
					if (adir)
						return -1;
					return 1;
				}
			}

			switch (sort_order){
			case SortOrder.Name:
				return string.Compare (a.Name, b.Name);
				
			case SortOrder.Extension:
				var sa = Path.GetExtension (a.Name);
				var sb = Path.GetExtension (b.Name);
				return string.Compare (sa, sb);
				
			case SortOrder.ModifyTime:
				return DateTime.Compare (a.Info.LastWriteTimeUtc, b.Info.LastWriteTimeUtc);
				
			case SortOrder.AccessTime:
				return DateTime.Compare (a.Info.LastAccessTimeUtc, b.Info.LastAccessTimeUtc);
				
			case SortOrder.ChangeTime:
				return DateTime.Compare (a.Info.LastStatusChangeTimeUtc, b.Info.LastStatusChangeTimeUtc);
				
			case SortOrder.Size:
				long r = a.Info.Length - b.Info.Length;
				if (r < 0)
					return -1;
				if (r > 0)
					return 1;
				return 0;
				
			case SortOrder.Inode:
				r = a.Info.Inode - b.Info.Inode;
				if (r < 0)
					return -1;
				if (r > 0)
					return 1;
				return 0;
			}
			return 0;
		}
		
		Panel (Shell shell, string path, int x, int y, int w, int h) : base (x, y, w, h, path)
		{
			this.shell = shell;
			CanFocus = true;
			Capacity = h - 2;
			SetCurrentPath (path, false);
			CurrentPath = path;
		}

		void SetCurrentPath (string path, bool refresh)
		{
			current_path = path;
			Title = Path.GetFullPath (path);
			listing = Listing.LoadFrom (current_path, CompareNodes);
			top = 0;
			selected = 0;
			marked = 0;

			if (refresh)
				Redraw ();
		}
		
		public int Capacity { get; private set; }
		public string CurrentPath {
			get {
				return current_path;
			}

			set {
				SetCurrentPath (value, true);
			}
		}
		
		public override void Redraw ()
		{
			base.Redraw ();
			Curses.attrset (ContainerColorNormal);
			int files = listing.Count;
			
			for (int i = 0; i < Capacity; i++){
				if (i + top >= files)
					break;

				DrawItem (top+i, top+i == selected);
			}
		}

		public override bool HasFocus {
			get {
				return base.HasFocus;
			}

			set {
				if (value)
					shell.CurrentPanel = this;
				base.HasFocus = value;
			}
		}

		public void DrawItem (int nth, bool is_selected)
		{
			char ch;
			
			if (nth >= listing.Count)
				throw new Exception ("overflow");

			is_selected = HasFocus && is_selected;
				
			Move (y + (nth-top) + 1, x + 1);
			
			Listing.FileNode node = listing [nth];
			int color;

			if (node == null)
				throw new Exception (String.Format ("Problem fetching item {0}", nth));

			if (node.Info.IsDirectory){
				color = is_selected ? ColorFocus : ColorDir;
				ch = '/';
			} else {
				color = is_selected ? ColorFocus : ColorNormal;
				ch = ' ';
			}
			if (node.Marked)
				color = is_selected ? ColorHotFocus : ColorHotNormal;

			Curses.attrset (color);
			for (int i = 0; i < node.Nested; i++)
				Curses.addstr ("  ");
			Curses.addch (ch);
			Curses.addstr (node.Name);
		}
		
		public override void DoSizeChanged ()
		{
			base.DoSizeChanged ();

			if (x == 0){
				w = Application.Cols/2;
			} else {
				w = Application.Cols/2+Application.Cols%2;
				x = Application.Cols/2;
			}
			
			h = Application.Lines-4;

			Capacity = h - 2;
		}
		
		public static Panel Create (Shell shell, string kind, int taken)
		{
			var height = Application.Lines - taken;
			
			switch (kind){
			case "left":
				return new Panel (shell, Environment.CurrentDirectory, 0, 1, Application.Cols/2, height);
					
			case "right":
				return new Panel (shell, Environment.GetFolderPath (Environment.SpecialFolder.Personal), Application.Cols/2, 1, Application.Cols/2+Application.Cols%2, height);
			}
			return null;
		}

		bool MoveDown ()
		{
			if (selected == listing.Count-1)
				return true;
			
			DrawItem (selected, false);
			selected++;
			if (selected-top >= Capacity){
				top += Capacity/2;
				if (top > listing.Count - Capacity)
					top = listing.Count - Capacity;
				Redraw ();
			} else  {
				DrawItem (selected, true);
			}
			return true;
		}

		bool MoveUp ()
		{
			if (selected == 0)
				return true;
			
			DrawItem (selected, false);
			selected--;
			if (selected < top){
				top -= Capacity/2;
				if (top < 0)
					top = 0;
				Redraw ();
			} else 
				DrawItem (selected, true);
			
			return true;
		}

		void PageDown ()
		{
			if (selected == listing.Count-1)
				return;

			int scroll = Capacity;
			if (top > listing.Count - 2 * Capacity)
				scroll = listing.Count - scroll - top;
			if (top + scroll < 0)
				scroll = -top;
			top += scroll;
			
			if (scroll == 0)
				selected = listing.Count-1;
			else {
				selected += scroll;
				if (selected > listing.Count)
					selected = listing.Count-1;
			}
			if (top > listing.Count)
				top = listing.Count-1;
			Redraw ();
		}

		void PageUp ()
		{
			if (selected == 0)
				return;
			if (top == 0){
				DrawItem (selected, false);
				selected = 0;
				DrawItem (selected, true);
			} else {
				top -= Capacity;
				selected -= Capacity;

				if (selected < 0)
					selected = 0;
				if (top < 0)
					top = 0;
				Redraw ();
			}
		}

		public bool CanExpandSelected {
			get {
				return listing [selected] is Listing.DirNode;
			}
		}

		public void ExpandSelected ()
		{
			var dn = listing [selected] as Listing.DirNode;
			if (dn == null)
				return;

			listing.Expand (selected);
			Redraw ();
		}

		public void CollapseAction ()
		{
			var dn = listing [selected] as Listing.DirNode;

			// If it is a regular file, navigate to the directory
			if (dn == null || dn.Expanded == false){
				for (int i = selected-1; i >= 0; i--){
					if (listing [i] is Listing.DirNode){
						selected = i;
						if (selected < top)
							top = selected;
						Redraw ();
						return;
					}
				}
				return;
			}

			listing.Collapse (selected);
			Redraw ();
		}

		//
		// Handler for the return key on an item
		//
		void Action ()
		{
			var node = listing [selected];

			if (node is Listing.DirNode){
				string focus = node is Listing.DirNodeDotDot ? Path.GetFileName (Title) : null;
				SetCurrentPath (Path.Combine (CurrentPath, listing.GetPathAt (selected)), false);

				if (focus != null){
					int idx = listing.NodeWithName (focus);
					Console.WriteLine ("Got: {0}", idx);
					if (idx != -1){
						selected = idx;

						// This could use some work to center on going up.
						if (selected >= Capacity){
							top = selected;
						}
					}
				}
				Redraw ();
			}
		}

		public void Copy (string target_dir)
		{
			var msg_file = "Copy file \"{0}\" to: ";
			int dlen = 68;
			int ilen = dlen-6;
			var d = new Dialog (dlen, 8, "Copy");

			if (marked > 1)
				d.Add (new Label (1, 0, String.Format ("Copy {0} files", marked)));
			else
				d.Add (new Label (1, 0, String.Format (msg_file, listing.GetPathAt (selected).Ellipsize (ilen-msg_file.Length))));

			var e = new Entry (1, 1, ilen, target_dir ?? "");
			d.Add (e);

			bool proceed = false;
			var b = new Button (0, 0, "Ok", true);
			b.Clicked += delegate {
				d.Running = false;
				proceed = true;
			};
			d.AddButton (b);
			b = new Button (0, 0, "Cancel", true);
			b.Clicked += (o,s) => d.Running = false;
			d.AddButton (b);
			
			Application.Run (d);
			if (!proceed)
				return;

			var progress = new Progress ("Copying", marked > 0 ? marked : 1);
			using (var ctx = new CopyOperation (progress)){
				foreach (var f in listing){
					if (!f.Marked)
						continue;
					
					ctx.Perform (CurrentPath, listing.GetPathAt (f.StartIdx), f is Listing.DirNode, f.Info.Protection, target_dir);
					progress.Step ();
				}
			}
		}
		
		public override bool ProcessKey (int key)
		{
			switch (key){
			case Curses.KeyUp:
			case 16: // Control-p
				return MoveUp ();
				
			case Curses.KeyDown:
			case 14: // Control-n
				return MoveDown ();

			case 22: // Control-v
			case Curses.KeyNPage:
				PageDown ();
				break;

			case (int) '\n':
				Action ();
				break;
				
			case Curses.KeyPPage:
			case (int)'v' + Curses.KeyAlt:
				PageUp ();
				break;

			case Curses.KeyInsertChar:
			case 20: // Control-t
				if (listing [selected].Name == "..")
					return true;
				listing [selected].Marked = !listing [selected].Marked;
				if (listing [selected].Marked)
					marked++;
				else
					marked--;
				MoveDown ();
				return true;
					
			default:
				return false;
			}
			return true;
		}
	}
}
