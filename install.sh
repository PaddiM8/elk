#!/bin/sh

cd build/*
tar -czf elk.tar.gz usr
sudo tar -xvf elk.tar.gz -C /
