//
// mc.cs: Panel controls
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Licensed under the MIT X11 license
//
using Mono.Terminal;

namespace MouselessCommander {

	public class Panel : Frame {
		void Bar () {}
			
		Panel (string path, int x, int y, int w, int h) : base (x, y, w, h, path)
		{
		}

		public static Panel Create (string kind, int height)
		{
			switch (kind){
			case "left":
				return new Panel ("/tmp", 0, 1, Application.Cols/2, height);
					
			case "right":
				return new Panel ("/home/miguel", Application.Cols/2, 1, Application.Cols/2+Application.Cols%2, height);
			}
			return null;
		}
	}
}