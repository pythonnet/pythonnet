def test_raise_exception(number=3, astring='abc'):
    raise ValueError("testing for memory leak")
    return astring * int(number)

def test_raise_exception2(number, astring):
    #raise ValueError("testing for memory leak")
    #astring * int(number)
    return "test" 
