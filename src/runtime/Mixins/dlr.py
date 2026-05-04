"""
Implements helpers for Dynamic Language Runtime (DLR) types.
"""

class DynamicMetaObjectProviderMixin:
    def __dir__(self):
        names = set(super().__dir__())

        get_dynamic_member_names = getattr(self, "GetDynamicMemberNames", None)
        if callable(get_dynamic_member_names):
            try:
                for name in get_dynamic_member_names():
                    if isinstance(name, str):
                        names.add(name)
            except Exception:
                pass

        return list(sorted(names))
