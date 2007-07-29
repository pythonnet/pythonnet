// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Collections;
using System.Windows.Forms;

namespace Python.Test {

    //========================================================================
    // Supports CLR class unit tests.
    //========================================================================

    public class ClassTest {

        public static ArrayList GetArrayList() {
            ArrayList list = new ArrayList();
            for (int i = 0; i < 10; i++) {
                list.Add(i);
            }
            return list;
        }

        public static Hashtable GetHashtable() {
            Hashtable dict = new Hashtable();
            dict.Add("one", 1);
            dict.Add("two", 2);
            dict.Add("three", 3);
            dict.Add("four", 4);
            dict.Add("five", 5);
            return dict;
        }

        public static IEnumerator GetEnumerator() {
            string temp = "test string";
            return temp.GetEnumerator();
        }


    }


    public class ClassCtorTest1 {
        public string value;

        public ClassCtorTest1() {
            value = "default";
        }

    }

    public class ClassCtorTest2 {
        public string value;

        public ClassCtorTest2(string v) {
            value = v;
        }

    }

    internal class InternalClass {


    }


}
