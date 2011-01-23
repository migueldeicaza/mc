using System;
using System.IO;
using Mono.Terminal;
using System.Text;

namespace MouselessCommander {

	public class ViewWidget : Widget {
		public bool Wrap { get; set; }
		
		// When in "cooked" mode, this reads encoded text
		StreamReader reader;

		// Encoding used to read
		Encoding encoding;

		// When in raw mode, we read directly from this stream
		Stream source;

		// The top byte displayed by the view
		long top_byte;

		// The first byte in the file that has contents (skipping the BOM)
		long first_file_byte;

		// Whether we are doing a Raw rendering, or a processed one.
		bool Raw;

		public ViewWidget (int x, int y, int w, int h, bool raw, Stream source) : base (x, y, w, h)
		{ 
			this.source = source;
			this.Raw = raw;

			CanFocus = true;
			DetectEncoding ();
			top_byte = first_file_byte;
		}

		static Encoding utf8 = new UTF8Encoding (false, false);
		static Encoding utf16le = new UnicodeEncoding (false, false);
		static Encoding utf16be = new UnicodeEncoding (true, false);
		static Encoding utf32le = new UTF32Encoding (false, false);
		static Encoding utf32be = new UTF32Encoding (true, false);

		int GetChar ()
		{
			if (Raw)
				return source.ReadByte ();
			else
				return reader.Read ();
		}
		
		void DetectEncoding ()
		{
			if (Raw)
				return;

			first_file_byte = 0;
			encoding = Encoding.UTF8;
			
			// Try to detect the encoding
			byte [] buffer = new byte [4];
			
			var n = source.Read (buffer, 0, 4);
			if (n == -1)
				return;
			if (n > 1 && buffer [0] == 0xfe && buffer [1] == 0xff){
				encoding = utf16be;
				first_file_byte = 2;
			} else if (n > 1 && buffer [0] == 0xff && buffer [1] == 0xfe){
				if (n > 3 && buffer [2] == 0 && buffer [3] == 0){
					encoding = utf32le;
					first_file_byte = 4;
				} else {
					encoding = utf16le;
					first_file_byte = 2;
				}
			} else if (n > 3 && buffer [0] == 0 && buffer [1] == 0 && buffer [2] == 0xfe && buffer [3] == 0xff){
				encoding = utf32be;
				first_file_byte = 4;
			}
			if (n > 2 && buffer [0] == 0xef && buffer [1] == 0xbb && buffer [2] == 0xbf){
				encoding = utf8;
				first_file_byte = 3;
			}
			source.Position = first_file_byte;
			reader = new StreamReader (source, encoding);
		}

		void SetPosition (long position)
		{
			source.Position = position;
			if (Raw)
				return;
			reader.DiscardBufferedData ();
		}

		// Fills with blanks from the current column/row
		// until the end of the widget area
		public void ClearToEnd (int ccol, int crow)
		{
			Log ("ccol={0} crow={1} h={2} w={3}", ccol, crow, h, w);
			for (int r = crow; r < h; r++){
				Move (r+y, ccol+x);
				for (int c = ccol; c < w; c++)
					Curses.addch (' ');
				ccol = 0;
			}
		}

		public void ClearToEnd (int ccol)
		{
			for (int c = ccol; c < w; c++)
				Curses.addch (' ');
		}
		
		public override void Redraw ()
		{
			int col = 0;
			bool skip_until_newline = false;
			
			SetPosition (top_byte);
			Curses.attrset (Container.ContainerColorNormal);
			Move (y, x);
			
			for (int row = 0; row < h; ){
				int c = GetChar ();
				switch (c){
					/* End of file */
				case -1:
					ClearToEnd (col, row);
					row = h;
					break;

				case 10:
					ClearToEnd (col);
					col = 0;
					row++;
					skip_until_newline = false;
					Move (y+row, x+col);
					continue;

				case 9:
					for (int nc = (col/8+1) * 8; col < nc; col++)
						Curses.addch (' ');

					continue;
					
				case 13:
					continue;
				}
				
				// Control chars or unicode > 0xffff
				if (c < 32 || c > 0xffff)
					c = '.';

				if (skip_until_newline)
					continue;
				
				Curses.addch ((char) c);
				col++;
				if (col > w){
					if (Wrap){
						col = 0;
						row++;
					} else
						skip_until_newline = true;
				}
			}
		}

		int ReadChar ()
		{
			if (Raw || encoding == utf8)
				return source.ReadByte ();
			
			var a = source.ReadByte ();
			var b = source.ReadByte ();
			if (a == -1 || b == -1)
				return 01;
			
			if (encoding == utf16le)
				return b << 8 | a;
			else if (encoding == utf16be)
				return a << 8 | b;

			var c = source.ReadByte ();
			var d = source.ReadByte ();
			if (c == -1 || d == -1)
				return -1;
			
			if (encoding == utf32be)
				return (a << 24) | (b << 16) | (c << 8) | d;
			else
				return (d << 24) | (c << 16) | (b << 8) | a;
		}
		
		// We can not use the StreamReader here
		//
		// Returns the new file offset where we start displaying, or -1 if we can not
		// scroll further.
		long ScanForward (int lines)
		{
			SetPosition (top_byte);
			for (int line = 0; line < lines; ){
				int b = ReadChar ();

				if (b == -1)
					return -1;
				
				if (Wrap){
				} else {
					if (b == 10)
						line++;
				}
			}
			return source.Position;
		}

		void MoveForward (int lines)
		{
			var newpos = ScanForward (lines);
			if (newpos == -1)
				return;
			top_byte = newpos;
			Redraw ();
		}
		
		public override bool ProcessKey (int key)
		{
			switch (key){
				// page down: space bar, control-v, page down:
			case 32: 
			case 22:
			case Curses.KeyNPage:
				MoveForward (h);
				break;

			case Curses.KeyDown:
				MoveForward (1);
				break;
				
			default:
				return false;
			}
			return true;
		}
	}

	public class FullView : Container {
		ViewWidget view;
		ButtonBar bar;
		
		string [] bar_labels = new string [] {
			"Help", "Wrap", "Quit", "Hex", "Line", "RxSrch", "Search", "Raw", "Unform", "Quit"
		};
		
		public FullView (Stream source) : base (0, 0, Application.Cols, Application.Lines)
		{
			view = new ViewWidget (0, 1, Application.Cols, Application.Lines-2, true, source);
			bar = new ButtonBar (bar_labels);
			bar.Action += delegate (int n){
				switch (n){
				case 3:
				case 10:
					Running = false;
					break;
				}
			};
			
			Add (view);
			Add (bar);
		}

		static public void Show (Stream source)
		{
			var full = new FullView (source);
			Application.Run (full);
		}
	}
}