import subprocess
import os

def runit(m1, m2, member):
    proc = subprocess.Popen([os.path.join(os.path.split(__file__)[0], 'bin', 'Python.DomainReloadTests.exe'), m1, m2, member])
    proc.wait()

    assert proc.returncode == 0

def test_remove_method():

    m1 = 'public static void TestMethod() {Console.WriteLine("from test method");}'
    m2 = ''
    member = 'TestMethod'
    runit(m1, m2, member)

def test_remove_member():

    m1 = 'public static int TestMember = -1;'
    m2 = ''
    member = 'TestMember'
    runit(m1, m2, member)