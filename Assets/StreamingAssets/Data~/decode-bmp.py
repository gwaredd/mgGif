#!/usr/bin/python3

import struct
import sys

data = open (sys.argv[1], 'rb').read ()

(width, height) = struct.unpack ('<II', data[18:26])
print ((width, height))
print (data[70:])
print (len (data[70:]) / 4)
