_UNSET = object()


class PyErr:
    def __init__(self, type=_UNSET, value=_UNSET, traceback=_UNSET):
        if type is not _UNSET:
            self.type = type
        if value is not _UNSET:
            self.value = value
        if traceback is not _UNSET:
            self.traceback = traceback
