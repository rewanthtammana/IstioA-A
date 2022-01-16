#!/bin/bash
parent_path=$( cd "$(dirname "${BASH_SOURCE[0]}")" ; pwd -P )
cd "$parent_path"

kubectl apply -f ../istio/egress-traffic-control-plane-only-mesh.yaml