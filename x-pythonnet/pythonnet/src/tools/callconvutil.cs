// CallConvUtil.cs - A utility to rewrite IL and insert calling
// convention metadata. This is needed to ensure that Python 
// type callbacks are called using cdecl rather than stdcall.
//
// Author:  Brian Lloyd <brian@zope.com>
//
// (c) 2002 Brian Lloyd

using System;
using System.IO;
using System.Collections;


public class CallConvUtil {

    static string ccAttr = 
	".custom instance void Python.Runtime.CallConvCdeclAttribute";

    static string modOpt = 
	"\n modopt([mscorlib]System.Runtime.CompilerServices.CallConvCdecl)";

    StreamReader reader;
    StreamWriter writer;


    public static int Main(string[] args) {
	CallConvUtil munger = new CallConvUtil();
	return munger.Run();
    }

    public int Run() {
	string inputFile = "Python.Runtime.il";
	string outputFile = "Python.Runtime.il2";
	string buff;
	string line;
	
	if (!File.Exists(inputFile)) {
         Console.WriteLine("{0} does not exist!", inputFile);
         return -1;
	}

	reader = File.OpenText(inputFile);
	writer = File.CreateText(outputFile);
	
	while ((line = reader.ReadLine())!= null) {

	    buff = line.Trim();
	    if (buff.StartsWith(".class ")) {
		ReadClass(line, false);
	    }
	    else {
		writer.WriteLine(line);
	    }

	}
      
	reader.Close();
	writer.Close();

	return 0;
    }

    public void ReadClass(string line, bool nested) {
	ArrayList lines = new ArrayList();
	bool hasAttr = false;
	string data;
	string buff;

	if (!nested) {
	    lines.Add(line);
	}

	while ((data = reader.ReadLine()) != null) {
	    buff = data.Trim();

	    if (buff.StartsWith(".class ")) {
		WriteBuffer(lines);
		writer.WriteLine(data);
		ReadClass(data, true);
		lines = new ArrayList();
	    }

	    else if (buff.StartsWith(ccAttr)) {
		hasAttr = true;
		lines.Add(data);
	    }

	    else if ( (!hasAttr) && buff.StartsWith(".method ")) {
		WriteBuffer(lines);
		ReadMethod(data);
		lines = new ArrayList();
	    }
	    else if (buff.StartsWith("} // end of class")) {
		WriteBuffer(lines);
		writer.WriteLine(data);
		return;
	    }
	    else if (hasAttr && buff.StartsWith("Invoke(")) {
		WriteBuffer(lines);
		writer.WriteLine(modOpt);
		writer.WriteLine(data);
		lines = new ArrayList();

	    }
	    else {
		lines.Add(data);
	    }
	}
    }

    public void ReadMethod(string line) {
	ArrayList lines = new ArrayList();
	string mline = line;

	string data;
	string buff;

	while ((data = reader.ReadLine()) != null) {
	    buff = data.Trim();
	    if (buff.StartsWith(ccAttr)) {
		writer.WriteLine(mline);
		writer.WriteLine(modOpt);
		WriteBuffer(lines);
		writer.WriteLine(data);
		return;
	    }
	    else if (buff.StartsWith("} // end of method")) {
		writer.WriteLine(mline);
		WriteBuffer(lines);
		writer.WriteLine(data);
		return;
	    }
	    lines.Add(data);
	}
    }

    public void WriteBuffer(ArrayList data) {
	IEnumerator iter = data.GetEnumerator();
	while (iter.MoveNext()) {
	    writer.WriteLine((String)iter.Current);
	}
    }


}
