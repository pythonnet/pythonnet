"""
Implements collections.abc for common .NET types
https://docs.python.org/3/library/collections.abc.html
"""

import collections.abc as col

class IteratorMixin(col.Iterator):
    def close(self):
        if hasattr(self, 'Dispose'):
            self.Dispose()
        else:
            from System import IDisposable
            IDisposable(self).Dispose()

class IterableMixin(col.Iterable):
    pass

class SizedMixin(col.Sized):
    def __len__(self): return self.Count

class ContainerMixin(col.Container):
    def __contains__(self, item):
        if hasattr('self', 'Contains'):
            return self.Contains(item)
        else:
            from System.Collections.Generic import ICollection
            return ICollection(self).Contains(item)

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
    def __contains__(self, item): return self.ContainsKey(item)
    def keys(self): return self.Keys
    def items(self): return [(k,self.get(k)) for k in self.Keys]
    def values(self): return self.Values
    def __iter__(self): return self.Keys.__iter__()
    def get(self, key, default=None):
        existed, item = self.TryGetValue(key, None)
        return item if existed else default

class MutableMappingMixin(MappingMixin, col.MutableMapping):
    _UNSET_ = object()

    def __delitem__(self, key):
        self.Remove(key)

    def clear(self):
        self.Clear()

    def pop(self, key, default=_UNSET_):
        existed, item = self.TryGetValue(key, None)
        if existed:
            self.Remove(key)
            return item
        elif default == self._UNSET_:
            raise KeyError(key)
        else:
            return default

    def setdefault(self, key, value=None):
        existed, item = self.TryGetValue(key, None)
        if existed:
            return item
        else:
            self[key] = value
            return value

    def update(self, items, **kwargs):
        if isinstance(items, col.Mapping):
            for key, value in items.items():
                self[key] = value
        else:
            for key, value in items:
                self[key] = value

        for key, value in kwargs.items():
            self[key] = value
