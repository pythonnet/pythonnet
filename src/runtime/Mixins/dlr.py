"""
Implements helpers for Dynamic Language Runtime (DLR) types.
"""

class DynamicMetaObjectProviderMixin:
    def __dir__(self):
        names = set(super().__dir__())

        get_names = getattr(self, "GetDynamicMemberNames", None)
        if callable(get_names):
            try:
                names.update(get_names())
            except Exception:
                pass

        return list(sorted(names))
