This is just my 'useful mame scripts'

mklibrary.sh - this takes a directory full of zip or 7z files and stores their contents in a library directory named after their sha1 crc.  Run this on as many mame sets as you have.
mkcrc.sh - symlinks the sha1 filenames to crc filenames.
mkchdlib.sh - does roughly the same as mklibrary for chds
mksamples.sh - copies samples to a common directory, always selecting the largest of any two (which mostly works)

MameBuilder.exe - takes a dat/xml file and builds a romset based on the constructed database of files, in any permutation required.  As I tend to switch between different mame versions
a lot keeping the database (which is largely static between releases) is a lot simpler than multiple romsets and faster after the initial build.

