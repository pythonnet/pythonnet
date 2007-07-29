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
using System.IO;

namespace Python.Test {

    //========================================================================
    // These classes support the CLR constructor unit tests.
    //========================================================================

    public class EnumConstructorTest {

        public TypeCode value;

        public EnumConstructorTest(TypeCode v) {
            this.value = v;
        }

    }


    public class FlagsConstructorTest {

        public FileAccess value;

        public FlagsConstructorTest(FileAccess v) {
            this.value = v;
        }

    }


    public class StructConstructorTest {

        public Guid value;

        public StructConstructorTest(Guid v) {
            this.value = v;
        }

    }


    public class SubclassConstructorTest {

        public Control value;

        public SubclassConstructorTest(Control v) {
            this.value = v;
        }

    }


}
