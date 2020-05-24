"""
Implements collections.abc for common .NET types
https://docs.python.org/3.6/library/collections.abc.html
"""

import collections.abc as col

class IteratorMixin(col.Iterator):
    def close(self):
        self.Dispose()

class IterableMixin(col.Iterable):
    pass

class SizedMixin(col.Sized):
    def __len__(self): return self.Count

class ContainerMixin(col.Container):
    def __contains__(self, item): return self.Contains(item)

try:
    abc_Collection = col.Collection
except AttributeError:
    # Python 3.5- does not have collections.abc.Collection
    abc_Collection = col.Container

class CollectionMixin(SizedMixin, IterableMixin, ContainerMixin, abc_Collection):
    pass

class SequenceMixin(CollectionMixin, col.Sequence):
    pass

class MutableSequenceMixin(SequenceMixin, col.MutableSequence):
    pass

class MappingMixin(CollectionMixin, col.Mapping):
    def keys(self): return self.Keys
    def items(self): return self
    def values(self): return self.Values
    def __iter__(self): raise NotImplementedError
    def get(self, key):
        _, item = self.TryGetValue(key)
        return item

class MutableMappingMixin(MappingMixin, col.MutableMapping):
    def __delitem__(self, key):
        return self.Remove(key)
    def clear(self):
        self.Clear()
    def pop(self, key):
        return self.Remove(key)
    def setdefault(self, key, value):
        existed, item = self.TryGetValue(key)
        if existed:
            return item
        else:
            self[key] = value
            return value
    def update(self, items):
        for key, value in items:
            self[key] = value
