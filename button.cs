//
// button.cs: The button bar widget
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Licensed under the MIT X11 license
//
using System;
using Mono.Terminal;

namespace MouselessCommander {

	public class ButtonBar : Widget {
		string [] labels;

		public delegate void ButtonAction (int n);
		
		public ButtonBar (string [] labels) : base (0, Application.Lines-1, Application.Cols, 1)
		{
			this.labels = labels;
		}

		public override void Redraw ()
		{
			int x = 0;
			int y = Application.Lines-1;
			Move (y, 0);
			
			for (int i = 0; i < labels.Length; i++){
				Curses.attrset (Application.ColorBasic);
				Curses.addstr (i == 0 ? "1" : String.Format (" {0}", i+1));
				Curses.attrset (ColorFocus);
				Curses.addstr ("{0,-6}", labels [i]);
			}
		}

		public override void DoSizeChanged ()
		{
			y = Application.Lines-1;
		}
		
		void Raise (int n)
		{
			if (Action != null)
				Action (n);
		}

		public event ButtonAction Action;
		
		public override bool ProcessHotKey (int key)
		{
			if (key >= Curses.KeyF1 && key <= Curses.KeyF10){
				Raise (key - Curses.KeyF1 + 1);
			} else if (key >= (Curses.KeyAlt + '0') && (key <= (Curses.KeyAlt + '9'))){
				var n = (key - Curses.KeyAlt - '0');
				Raise (n == 0 ? n = 10 : n);
			} else
				return false;
			
			return true;
		}
	}
}