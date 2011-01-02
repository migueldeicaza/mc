using System;
using System.IO;

class debug {
	static StreamWriter log = File.CreateText ("log");
	
	static public void print (string msg)
	{
		log.WriteLine (msg);
		log.Flush ();
	}

	static public void print (string format, params object [] args)
	{
		log.WriteLine (format, args);
		log.Flush ();
	}
}