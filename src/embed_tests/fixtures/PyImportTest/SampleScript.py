def invokeMethod(instance, method_name):
    invokeMethodImpl(instance, method_name)

def invokeMethodImpl(instance, method_name):
    getattr(instance, method_name)()
