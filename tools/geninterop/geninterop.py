#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""
TypeOffset is a C# class that mirrors the in-memory layout of heap
allocated Python objects.

This script parses the Python C headers and outputs the TypeOffset
C# class.

Requirements:
    - pycparser
    - clang
"""

from __future__ import print_function

import logging
import os
import sys
import sysconfig
import subprocess

if sys.version_info.major > 2:
    from io import StringIO
else:
    from StringIO import StringIO

from pycparser import c_ast, c_parser

_log = logging.getLogger()
logging.basicConfig(level=logging.DEBUG)

PY_MAJOR = sys.version_info[0]
PY_MINOR = sys.version_info[1]

# rename some members from their C name when generating the C#
_typeoffset_member_renames = {
    "ht_name": "name",
    "ht_qualname": "qualname"
}


def _check_output(*args, **kwargs):
    """Check output wrapper for py2/py3 compatibility"""
    output = subprocess.check_output(*args, **kwargs)
    if PY_MAJOR == 2:
        return output
    return output.decode("ascii")


class AstParser(object):
    """Walk an AST and determine the members of all structs"""

    def __init__(self):
        self.__typedefs = {}
        self.__typedecls = {}
        self.__structs = {}
        self.__struct_stack = []
        self.__struct_members_stack = []
        self.__ptr_decl_depth = 0
        self.__struct_members = {}
        self.__decl_names = {}

    def get_struct_members(self, name):
        """return a list of (name, type) of struct members"""
        defs = self.__typedefs.get(name)
        if defs is None:
            return None
        node = self.__get_leaf_node(defs)
        name = node.name
        if name is None:
            name = defs.declname
        return self.__struct_members.get(name)

    def visit(self, node):
        if isinstance(node, c_ast.FileAST):
            self.visit_ast(node)
        elif isinstance(node, c_ast.Typedef):
            self.visit_typedef(node)
        elif isinstance(node, c_ast.TypeDecl):
            self.visit_typedecl(node)
        elif isinstance(node, c_ast.Struct):
            self.visit_struct(node)
        elif isinstance(node, c_ast.Decl):
            self.visit_decl(node)
        elif isinstance(node, c_ast.FuncDecl):
            self.visit_funcdecl(node)
        elif isinstance(node, c_ast.PtrDecl):
            self.visit_ptrdecl(node)
        elif isinstance(node, c_ast.IdentifierType):
            self.visit_identifier(node)

    def visit_ast(self, ast):
        for name, node in ast.children():
            self.visit(node)

    def visit_typedef(self, typedef):
        self.__typedefs[typedef.name] = typedef.type
        self.visit(typedef.type)

    def visit_typedecl(self, typedecl):
        self.__decl_names[typedecl.type] = typedecl.declname
        self.visit(typedecl.type)

    def visit_struct(self, struct):
        self.__structs[self.__get_struct_name(struct)] = struct
        if struct.decls:
            # recurse into the struct
            self.__struct_stack.insert(0, struct)
            for decl in struct.decls:
                self.__struct_members_stack.insert(0, decl.name)
                self.visit(decl)
                self.__struct_members_stack.pop(0)
            self.__struct_stack.pop(0)
        elif self.__ptr_decl_depth:
            # the struct is empty, but add it as a member to the current
            # struct as the current member maybe a pointer to it.
            self.__add_struct_member(struct.name)

    def visit_decl(self, decl):
        self.visit(decl.type)

    def visit_funcdecl(self, funcdecl):
        self.visit(funcdecl.type)

    def visit_ptrdecl(self, ptrdecl):
        self.__ptr_decl_depth += 1
        self.visit(ptrdecl.type)
        self.__ptr_decl_depth -= 1

    def visit_identifier(self, identifier):
        type_name = " ".join(identifier.names)
        self.__add_struct_member(type_name)

    def __add_struct_member(self, type_name):
        if not (self.__struct_stack and self.__struct_members_stack):
            return

        # add member to current struct
        current_struct = self.__struct_stack[0]
        member_name = self.__struct_members_stack[0]
        struct_members = self.__struct_members.setdefault(
            self.__get_struct_name(current_struct), [])

        # get the node associated with this type
        node = None
        if type_name in self.__typedefs:
            node = self.__get_leaf_node(self.__typedefs[type_name])
        elif type_name in self.__structs:
            node = self.__structs[type_name]

        # If it's a struct (and not a pointer to a struct) expand
        # it into the current struct definition
        if not self.__ptr_decl_depth and isinstance(node, c_ast.Struct):
            for decl in node.decls or []:
                self.__struct_members_stack.insert(0, decl.name)
                self.visit(decl)
                self.__struct_members_stack.pop(0)
        else:
            # otherwise add it as a single member
            struct_members.append((member_name, type_name))

    def __get_leaf_node(self, node):
        if isinstance(node, c_ast.Typedef):
            return self.__get_leaf_node(node.type)
        if isinstance(node, c_ast.TypeDecl):
            return self.__get_leaf_node(node.type)
        return node

    def __get_struct_name(self, node):
        return node.name or self.__decl_names.get(node) or "_struct_%d" % id(node)


class Writer(object):

    def __init__(self):
        self._stream = StringIO()

    def append(self, indent=0, code=""):
        self._stream.write("%s%s\n" % (indent * "    ", code))

    def extend(self, s):
        self._stream.write(s)

    def to_string(self):
        return self._stream.getvalue()


def preprocess_python_headers():
    """Return Python.h pre-processed, ready for parsing.
    Requires clang.
    """
    fake_libc_include = os.path.join(os.path.dirname(__file__),
                                     "fake_libc_include")
    include_dirs = [fake_libc_include]

    include_py = sysconfig.get_config_var("INCLUDEPY")
    include_dirs.append(include_py)

    include_args = [c for p in include_dirs for c in ["-I", p]]

    defines = [
        "-D", "__attribute__(x)=",
        "-D", "__inline__=inline",
        "-D", "__asm__=;#pragma asm",
        "-D", "__int64=long long",
        "-D", "_POSIX_THREADS"
    ]

    if os.name == 'nt':
        defines.extend([
            "-D", "__inline=inline",
            "-D", "__ptr32=",
            "-D", "__ptr64=",
            "-D", "__declspec(x)=",
        ])

    if hasattr(sys, "abiflags"):
        if "d" in sys.abiflags:
            defines.extend(("-D", "PYTHON_WITH_PYDEBUG"))
        if "m" in sys.abiflags:
            defines.extend(("-D", "PYTHON_WITH_PYMALLOC"))
        if "u" in sys.abiflags:
            defines.extend(("-D", "PYTHON_WITH_WIDE_UNICODE"))

    python_h = os.path.join(include_py, "Python.h")
    cmd = ["clang", "-pthread"] + include_args + defines + ["-E", python_h]

    # normalize as the parser doesn't like windows line endings.
    lines = []
    for line in _check_output(cmd).splitlines():
        if line.startswith("#"):
            line = line.replace("\\", "/")
        lines.append(line)
    return "\n".join(lines)



def gen_interop_head(writer):
    defines = [
        "PYTHON{0}{1}".format(PY_MAJOR, PY_MINOR)
    ]

    if hasattr(sys, "abiflags"):
        if "d" in sys.abiflags:
            defines.append("PYTHON_WITH_PYDEBUG")
        if "m" in sys.abiflags:
            defines.append("PYTHON_WITH_PYMALLOC")
        if "u" in sys.abiflags:
            defines.append("PYTHON_WITH_WIDE_UNICODE")

    filename = os.path.basename(__file__)
    defines_str = " && ".join(defines)
    class_definition = """
// Auto-generated by %s.
// DO NOT MODIFY BY HAND.


#if %s
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;

namespace Python.Runtime
{
""" % (filename, defines_str)
    writer.extend(class_definition)


def gen_interop_tail(writer):
    tail = """}
#endif
"""
    writer.extend(tail)


def gen_heap_type_members(parser, writer):
    """Generate the TypeOffset C# class"""
    members = parser.get_struct_members("PyHeapTypeObject")
    class_definition = """
    [StructLayout(LayoutKind.Sequential)]
    internal static partial class TypeOffset
    {
        // Auto-generated from PyHeapTypeObject in Python.h
"""

    # All the members are sizeof(void*) so we don't need to do any
    # extra work to determine the size based on the type.
    for name, tpy in members:
        name = _typeoffset_member_renames.get(name, name)
        class_definition += "        public static int %s = 0;\n" % name

    class_definition += """
        /* here are optional user slots, followed by the members. */
        public static int members = 0;
    }

"""
    writer.extend(class_definition)


def gen_structure_code(parser, writer, type_name, indent):
    members = parser.get_struct_members(type_name)
    if members is None:
        return False
    out = writer.append
    out(indent, "[StructLayout(LayoutKind.Sequential)]")
    out(indent, "internal struct %s" % type_name)
    out(indent, "{")
    for name, tpy in members:
        out(indent + 1, "public IntPtr %s;" % name)
    out(indent, "}")
    out()
    return True


def gen_supported_slot_record(writer, types, indent):
    out = writer.append
    out(indent, "internal static partial class SlotTypes")
    out(indent, "{")
    out(indent + 1, "public static readonly Type[] Types = {")
    for name in types:
        out(indent + 2, "typeof(%s)," % name)
    out(indent + 1, "};")
    out(indent, "}")
    out()


def main():
    # preprocess Python.h and build the AST
    python_h = preprocess_python_headers()
    parser = c_parser.CParser()
    ast = parser.parse(python_h)

    # extract struct members from the AST
    ast_parser = AstParser()
    ast_parser.visit(ast)

    writer = Writer()
    # generate the C# code
    gen_interop_head(writer)

    gen_heap_type_members(ast_parser, writer)
    slots_types = [
        "PyNumberMethods",
        "PySequenceMethods",
        "PyMappingMethods",
        "PyAsyncMethods",
        "PyBufferProcs",
    ]
    supported_types = []
    indent = 1
    for type_name in slots_types:
        if not gen_structure_code(ast_parser, writer, type_name, indent):
            continue
        supported_types.append(type_name)
    gen_supported_slot_record(writer, supported_types, indent)

    gen_interop_tail(writer)

    interop_cs = writer.to_string()
    if len(sys.argv) > 1:
        with open(sys.argv[1], "w") as fh:
            fh.write(interop_cs)
    else:
        print(interop_cs)


if __name__ == "__main__":
    sys.exit(main())
