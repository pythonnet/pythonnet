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

import os
import shutil
import sys
import sysconfig
import subprocess

from io import StringIO
from pathlib import Path
from pycparser import c_ast, c_parser

# rename some members from their C name when generating the C#
_typeoffset_member_renames = {
    "ht_name": "name",
    "ht_qualname": "qualname",
    "getitem": "spec_cache_getitem",
}


def _check_output(*args, **kwargs):
    return subprocess.check_output(*args, **kwargs, encoding="utf8")


class AstParser(object):
    """Walk an AST and determine the members of all structs"""

    def __init__(self):
        self._typedefs = {}
        self._typedecls = {}
        self._structs = {}
        self._struct_stack = []
        self._struct_members_stack = []
        self._ptr_decl_depth = 0
        self._struct_members = {}
        self._decl_names = {}

    def get_struct_members(self, name):
        """return a list of (name, type) of struct members"""
        defs = self._typedefs.get(name)
        if defs is None:
            return None
        node = self._get_leaf_node(defs)
        name = node.name
        if name is None:
            name = defs.declname
        return self._struct_members.get(name)

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
        for _name, node in ast.children():
            self.visit(node)

    def visit_typedef(self, typedef):
        self._typedefs[typedef.name] = typedef.type
        self.visit(typedef.type)

    def visit_typedecl(self, typedecl):
        self._decl_names[typedecl.type] = typedecl.declname
        self.visit(typedecl.type)

    def visit_struct(self, struct):
        if struct.decls:
            self._structs[self._get_struct_name(struct)] = struct
            # recurse into the struct
            self._struct_stack.insert(0, struct)
            for decl in struct.decls:
                self._struct_members_stack.insert(0, decl.name)
                self.visit(decl)
                self._struct_members_stack.pop(0)
            self._struct_stack.pop(0)
        elif self._ptr_decl_depth or self._struct_members_stack:
            # the struct is empty, but add it as a member to the current
            # struct as the current member maybe a pointer to it.
            self._add_struct_member(struct.name)

    def visit_decl(self, decl):
        self.visit(decl.type)

    def visit_funcdecl(self, funcdecl):
        self.visit(funcdecl.type)

    def visit_ptrdecl(self, ptrdecl):
        self._ptr_decl_depth += 1
        self.visit(ptrdecl.type)
        self._ptr_decl_depth -= 1

    def visit_identifier(self, identifier):
        type_name = " ".join(identifier.names)
        self._add_struct_member(type_name)

    def _add_struct_member(self, type_name):
        if not (self._struct_stack and self._struct_members_stack):
            return

        # add member to current struct
        current_struct = self._struct_stack[0]
        member_name = self._struct_members_stack[0]
        struct_members = self._struct_members.setdefault(
            self._get_struct_name(current_struct), []
        )

        # get the node associated with this type
        node = None
        if type_name in self._typedefs:
            node = self._get_leaf_node(self._typedefs[type_name])
            # If the struct was only declared when the typedef was created, its member
            # information will not have been recorded and we have to look it up in the
            # structs
            if isinstance(node, c_ast.Struct) and node.decls is None:
                if node.name in self._structs:
                    node = self._structs[node.name]
        elif type_name in self._structs:
            node = self._structs[type_name]

        # If it's a struct (and not a pointer to a struct) expand
        # it into the current struct definition
        if not self._ptr_decl_depth and isinstance(node, c_ast.Struct):
            for decl in node.decls or []:
                self._struct_members_stack.insert(0, decl.name)
                self.visit(decl)
                self._struct_members_stack.pop(0)
        else:
            # otherwise add it as a single member
            struct_members.append((member_name, type_name))

    def _get_leaf_node(self, node):
        if isinstance(node, c_ast.Typedef):
            return self._get_leaf_node(node.type)
        if isinstance(node, c_ast.TypeDecl):
            return self._get_leaf_node(node.type)
        return node

    def _get_struct_name(self, node):
        return node.name or self._decl_names.get(node) or "_struct_%d" % id(node)


class Writer(object):
    def __init__(self):
        self._stream = StringIO()

    def append(self, indent=0, code=""):
        self._stream.write("%s%s\n" % (indent * "    ", code))

    def extend(self, s):
        self._stream.write(s)

    def to_string(self):
        return self._stream.getvalue()


def preprocess_python_headers(*, cc=None, include_py=None):
    """Return Python.h pre-processed, ready for parsing.
    Requires clang.
    """
    this_path = Path(__file__).parent

    fake_libc_include = this_path / "fake_libc_include"
    include_dirs = [fake_libc_include]

    if cc is None:
        cc = shutil.which("clang")
    if cc is None:
        cc = shutil.which("gcc")
    if cc is None:
        raise RuntimeError("No suitable C compiler found, need clang or gcc")

    if include_py is None:
        include_py = sysconfig.get_config_var("INCLUDEPY")
    include_py = Path(include_py)

    include_dirs.append(include_py)

    include_args = [c for p in include_dirs for c in ["-I", str(p)]]

    # fmt: off
    defines = [
        "-D", "__attribute__(x)=",
        "-D", "__inline__=inline",
        "-D", "__asm__=;#pragma asm",
        "-D", "__int64=long long",
        "-D", "_POSIX_THREADS",
    ]

    if sys.platform == "win32":
        defines.extend([
            "-D", "__inline=inline",
            "-D", "__ptr32=",
            "-D", "__ptr64=",
            "-D", "__declspec(x)=",
        ])
    #fmt: on

    if hasattr(sys, "abiflags"):
        if "d" in sys.abiflags:
            defines.extend(("-D", "PYTHON_WITH_PYDEBUG"))
        if "u" in sys.abiflags:
            defines.extend(("-D", "PYTHON_WITH_WIDE_UNICODE"))

    python_h = include_py / "Python.h"
    cmd = [cc, "-pthread"] + include_args + defines + ["-E", str(python_h)]

    # normalize as the parser doesn't like windows line endings.
    lines = []
    for line in _check_output(cmd).splitlines():
        if line.startswith("#"):
            line = line.replace("\\", "/")
        lines.append(line)
    return "\n".join(lines)


def gen_interop_head(writer, version, abi_flags):
    filename = os.path.basename(__file__)
    class_definition = f"""
// Auto-generated by {filename}.
// DO NOT MODIFY BY HAND.

// Python {".".join(version[:2])}: ABI flags: '{abi_flags}'

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using Python.Runtime.Native;

namespace Python.Runtime
{{"""
    writer.extend(class_definition)


def gen_interop_tail(writer):
    tail = """}
"""
    writer.extend(tail)


def gen_heap_type_members(parser, writer, type_name):
    """Generate the TypeOffset C# class"""
    members = parser.get_struct_members("PyHeapTypeObject")
    class_definition = f"""
    [SuppressMessage("Style", "IDE1006:Naming Styles",
                     Justification = "Following CPython",
                     Scope = "type")]

    [StructLayout(LayoutKind.Sequential)]
    internal class {type_name} : GeneratedTypeOffsets, ITypeOffsets
    {{
        public {type_name}() {{ }}
        // Auto-generated from PyHeapTypeObject in Python.h
"""

    # All the members are sizeof(void*) so we don't need to do any
    # extra work to determine the size based on the type.
    for name, _type in members:
        name = _typeoffset_member_renames.get(name, name)
        class_definition += "        public int %s  { get; private set; }\n" % name

    class_definition += """    }
"""
    writer.extend(class_definition)


def gen_structure_code(parser, writer, type_name, indent):
    members = parser.get_struct_members(type_name)
    if members is None:
        return False
    out = writer.append
    out(indent, "[StructLayout(LayoutKind.Sequential)]")
    out(indent, f"internal struct {type_name}")
    out(indent, "{")
    for name, _type in members:
        out(indent + 1, f"public IntPtr {name};")
    out(indent, "}")
    out()
    return True


def main(*, cc=None, include_py=None, version=None, out=None):
    # preprocess Python.h and build the AST
    python_h = preprocess_python_headers(cc=cc, include_py=include_py)
    parser = c_parser.CParser()
    ast = parser.parse(python_h)

    # extract struct members from the AST
    ast_parser = AstParser()
    ast_parser.visit(ast)

    writer = Writer()

    if include_py and not version:
        raise RuntimeError("If the include path is overridden, version must be "
                "defined"
                )

    if version:
        version = version.split('.')
    else:
        version = sys.version_info

    # generate the C# code
    abi_flags = getattr(sys, "abiflags", "").replace("m", "")
    gen_interop_head(writer, version, abi_flags)

    type_name = f"TypeOffset{version[0]}{version[1]}{abi_flags}"
    gen_heap_type_members(ast_parser, writer, type_name)

    gen_interop_tail(writer)

    interop_cs = writer.to_string()
    if not out or out == "-":
        print(interop_cs)
    else:
        with open(out, "w") as fh:
            fh.write(interop_cs)


if __name__ == "__main__":
    import argparse

    a = argparse.ArgumentParser("Interop file generator for Python.NET")
    a.add_argument("--cc", help="C compiler to use, either clang or gcc")
    a.add_argument("--include-py", help="Include path of Python")
    a.add_argument("--version", help="Python version")
    a.add_argument("--out", help="Output path", default="-")
    args = a.parse_args()

    sys.exit(main(
        cc=args.cc,
        include_py=args.include_py,
        out=args.out,
        version=args.version
        ))
