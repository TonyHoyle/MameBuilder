#!/bin/bash
SRC="/raid/mame/library"
DEST="/raid/mame/library/crc";
find "$SRC" -type f -name '*.rom' -print0 | 
while IFS= read -d $'\0' -r i; do
	SHA=$(basename $i .rom)
	CRC=$(crc32 "$i")
	DF="$DEST/"$(echo $CRC | awk '{ print substr($1, 1, 2) "/" $1 ".rom" }')
	DD="$DEST/"$(echo $CRC | awk '{ print substr($1, 1, 2) }')
	if [ ! -s "$DF" ]; then
		mkdir -p "$DD"
		echo "$SHA -> $CRC"
		ln -s "$i" "$DF"
	fi
done

