#!/bin/sh

# Downloads the docs from the latest GitHub release.
# This is used by Netlify.
wget https://github.com/PaddiM8/elk/releases/latest/download/docs.tar.xz
rm -rf dist
mkdir dist
tar -xvf docs.tar.xz -C dist/
