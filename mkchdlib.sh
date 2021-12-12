#!/bin/bash
CHDS="$1"
DEST="/raid/mame/library/chd"
IFS=$'\n'
for i in $(find "$CHDS" -name *.chd -print); do
	echo $(basename $i .chd)
	SHA=$(chdman info -i $i | grep ^SHA1 | sed 's/SHA1:[[:space:]]*//')
	DF="$DEST/"$(echo $SHA | awk '{ print substr($1, 1, 2) "/" $1 ".dat" }')
	if [ ! -s "$DF" ]; then
		echo $SHA
		install -D "$i" "$DF"
	fi
done

