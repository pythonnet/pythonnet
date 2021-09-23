_UNSET = object()

class PyErr:
    def __init__(self, type=_UNSET, value=_UNSET, traceback=_UNSET):
        if not(type is _UNSET):
            self.type = type
        if not(value is _UNSET):
            self.value = value
        if not(traceback is _UNSET):
            self.traceback = traceback
