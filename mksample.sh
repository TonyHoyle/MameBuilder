#!/bin/bash
SAMPLES="$1/samples"
DEST="/raid/mame/library/samples"
IFS=$'\n'
for i in $SAMPLES/*.zip; do
	GAME=$(basename $i .zip)
	echo $GAME
	unzip -q "$i" -d /tmp/$$
	pushd /tmp/$$ >/dev/null
	for j in *; do
		DF="$DEST/$GAME/$j"
		if [ -f $DF ]; then
			SIZESRC=$(stat -c "%s" "$j")
			SIZEDF=$(stat -c "%s" "$DF")
			if [ $SIZEDF -lt $SIZESRC ]; then
				echo $DF
				install -D "$j" "$DF"
			fi
		else
			echo $DF
			install -D "$j" "$DF"
		fi
	done
	popd >/dev/null
	rm -rf /tmp/$$
done

