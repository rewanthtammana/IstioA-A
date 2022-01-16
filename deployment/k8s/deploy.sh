#!/bin/bash

echo "create ns and enable istio injection..."
sh create-ns.sh

echo "creating mesh wide sidecar resoruce to restrict the egress trafic of all sidecars to istio control plane only..."
sh egress-traffic-control-plane-only-mesh.sh

echo "creating mesh-wide policy that denies all requests that do not explicitly specify an ALLOW policy..."
sh policy-deny-all-mesh.sh

echo "deploying accounts..."
sh accounts.sh

echo "deploying customers..."
sh customers.sh

echo "deploying transfers..."
sh transfers.sh