//
// util.cs: Utility functions and classes
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Licensed under the MIT X11 license
//
using System;
using Mono.Terminal;
using Mono.Unix.Native;

namespace MouselessCommander {

	public class BasicInteraction : Dialog, IUserInteraction  {
		public BasicInteraction (string title) : base (68, 12, title)
		{
		}
		
		public OResult Query (OResult flags, string errormsg, string condition, string file)
		{
			var s = String.Format (condition, file);
			int len = Math.Min (s.Length, Application.Cols-8);

			var d = new Dialog (len, 8, "Error");
			d.ErrorColors ();
			d.Add (new Label (1, 1, errormsg));
			d.Add (new Label (1, 2, String.Format (condition, file.Ellipsize (len-condition.Length))));

			OResult result = OResult.Ignore;
			Button b;
			if ((flags & OResult.Retry) == OResult.Retry){
				b = new Button (0, 0, "Retry", true);
				b.Clicked += delegate {
					result = OResult.Retry;
					d.Running = false;
				};
				d.Add (b);
			}
			if ((flags & OResult.Ignore) == OResult.Ignore){
				b = new Button (0, 0, "Ignore", true);
				b.Clicked += delegate {
					result = OResult.Ignore;
					d.Running = false;
				};
				d.Add (b);
			}
			if ((flags & OResult.Cancel) == OResult.Cancel){
				b = new Button (0, 0, "Cancel", true);
				b.Clicked += delegate {
					result = OResult.Cancel;
					d.Running = false;
				};
				d.Add (b);
			}
			Application.Run (d);
			return result;
		}
	}

	public class ProgressInteraction : BasicInteraction, IProgressInteraction {
		public ProgressInteraction (string title, int steps) : base (title)
		{
		}

		public void Step ()
		{
		}
	}
	
	public static class StringExtensions {
		public static string Ellipsize (this string source, int width)
		{
			if (source.Length <= width)
				return source;
			else
				return source.Substring (0, width/2) + "~" + source.Substring (source.Length - 1 - width/2);
		}
	}
}