#!/bin/bash
ROMS="$1"
DEST="/raid/mame/library"
IFS=$'\n'
for i in $ROMS/*.{zip,7z}; do
	# If one of zip,7z doesn't exist bash returns a junk string.
	if [ ! -f $i ]; then continue; fi;
        if [ "${i##*.}" == 'zip' ]; then
	  unzip -q "$i" -d /tmp/$$
        elif [ "${i##*.}" == '7z' ]; then
	  7z e -y -o/tmp/$$ "$i" >/dev/null
        else
          continue
        fi
	if [ ! -d /tmp/$$ ]; then continue; fi
	echo ${i%.*}
	pushd /tmp/$$ >/dev/null
	for j in *; do
		SHA=$(sha1sum -b "$j" | cut -f1 -d' ')
		DF="$DEST/"$(echo $SHA | awk '{ print substr($1, 1, 2) "/" $1 ".rom" }')
		if [ ! -s "$DF" ]; then
			echo "$(basename "$j") -> $SHA"
			install -D "$j" "$DF"
		fi
	done
	popd >/dev/null
	rm -rf /tmp/$$
done

