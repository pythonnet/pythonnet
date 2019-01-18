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

    def get_struct_members(self, name):
        """return a list of (name, type) of struct members"""
        if name in self.__typedefs:
            node = self.__get_leaf_node(self.__typedefs[name])
            name = node.name
        if name not in self.__struct_members:
            raise Exception("Unknown struct '%s'" % name)
        return self.__struct_members[name]

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
        return node.name or "_struct_%d" % id(node)


def preprocess_python_headers():
    """Return Python.h pre-processed, ready for parsing.
    Requires clang.
    """
    fake_libc_include = os.path.join(os.path.dirname(__file__),
                                     "fake_libc_include")
    include_dirs = [fake_libc_include]

    include_py = sysconfig.get_config_var("INCLUDEPY")
    include_dirs.append(include_py)

    defines = [
        "-D", "__attribute__(x)=",
        "-D", "__inline__=inline",
        "-D", "__asm__=;#pragma asm",
        "-D", "__int64=long long"
    ]

    if hasattr(sys, "abiflags"):
        if "d" in sys.abiflags:
            defines.extend(("-D", "PYTHON_WITH_PYDEBUG"))
        if "m" in sys.abiflags:
            defines.extend(("-D", "PYTHON_WITH_PYMALLOC"))
        if "u" in sys.abiflags:
            defines.extend(("-D", "PYTHON_WITH_WIDE_UNICODE"))

    python_h = os.path.join(include_py, "Python.h")
    cmd = ["clang", "-I"] + include_dirs + defines + ["-E", python_h]

    # normalize as the parser doesn't like windows line endings.
    lines = []
    for line in _check_output(cmd).splitlines():
        if line.startswith("#"):
            line = line.replace("\\", "/")
        lines.append(line)
    return "\n".join(lines)


def gen_interop_code(members):
    """Generate the TypeOffset C# class"""

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
// DO NOT MODIFIY BY HAND.


#if %s
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;

namespace Python.Runtime
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal class TypeOffset
    {
        static TypeOffset()
        {
            Type type = typeof(TypeOffset);
            FieldInfo[] fi = type.GetFields();
            int size = IntPtr.Size;
            for (int i = 0; i < fi.Length; i++)
            {
                fi[i].SetValue(null, i * size);
            }
        }

        public static int magic()
        {
            return ob_size;
        }

        // Auto-generated from PyHeapTypeObject in Python.h
""" % (filename, defines_str)

    # All the members are sizeof(void*) so we don't need to do any
    # extra work to determine the size based on the type.
    for name, tpy in members:
        name = _typeoffset_member_renames.get(name, name)
        class_definition += "        public static int %s = 0;\n" % name

    class_definition += """
        /* here are optional user slots, followed by the members. */
        public static int members = 0;
    }
}

#endif
"""
    return class_definition


def main():
    # preprocess Python.h and build the AST
    python_h = preprocess_python_headers()
    parser = c_parser.CParser()
    ast = parser.parse(python_h)

    # extract struct members from the AST
    ast_parser = AstParser()
    ast_parser.visit(ast)

    # generate the C# code
    members = ast_parser.get_struct_members("PyHeapTypeObject")
    interop_cs = gen_interop_code(members)

    if len(sys.argv) > 1:
        with open(sys.argv[1], "w") as fh:
            fh.write(interop_cs)
    else:
        print(interop_cs)


if __name__ == "__main__":
    sys.exit(main())
