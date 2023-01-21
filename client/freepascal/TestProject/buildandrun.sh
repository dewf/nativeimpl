#!/bin/bash
fpc AppRunner.lpr
[ $? -eq 0 ] || exit 1
./AppRunner.exe

