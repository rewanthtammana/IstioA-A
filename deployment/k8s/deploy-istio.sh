#!/bin/bash

parent_path=$( cd "$(dirname "${BASH_SOURCE[0]}")" ; pwd -P )
cd "$parent_path"

cd istio-1.12.1

export PATH=$PWD/bin:$PATH

istioctl install --set profile=demo -y