#!/usr/bin/python3

import struct
import sys
import zlib

data = open (sys.argv[1], 'rb').read ()
(width, height) = struct.unpack ('>II', data[16:24])
print (width, height)

idat = data[33:]
(idat_length, ) = struct.unpack ('>I', data[33:37])
print (idat_length)
idat_data = idat[8:8 + idat_length]
print (idat_data)
print (zlib.decompress (idat_data))
