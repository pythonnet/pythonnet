# ===========================================================================
# This software is subject to the provisions of the Zope Public License,
# Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
# THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
# WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
# FOR A PARTICULAR PURPOSE.
# ===========================================================================

"""Run all of the unit tests for this package over and over,
   in order to provide for better profiling."""

def main():
    import sys, os, gc, time

    dirname = os.path.split(__file__)
    sys.path.append(dirname)
    import runtests
    
    gc.set_debug(gc.DEBUG_LEAK)

    start = time.clock()

    for i in range(50):
        print 'iteration: %d' % i
        runtests.main()

    stop = time.clock()
    took = str(stop - start)
    print 'Total Time: %s' % took


    for item in gc.get_objects():
        print item, sys.getrefcount(item)



if __name__ == '__main__':
    main()
    sys.exit(0)
    

