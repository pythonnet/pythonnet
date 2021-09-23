import System.Collections.Generic as C

def test_contains():
    l = C.List[int]()
    l.Add(42)
    assert 42 in l
    assert 43 not in l

def test_dict_items():
    d = C.Dictionary[int, str]()
    d[42] = "a"
    items = d.items()
    assert len(items) == 1
    k,v = items[0]
    assert k == 42
    assert v == "a"
