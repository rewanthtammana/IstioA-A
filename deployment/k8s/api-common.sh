#!/bin/bash

HELM_CHART_DIR=""
HELM_RELEASE=""
NAMESPACE=""

apply () {
    helm upgrade ${HELM_RELEASE} ${HELM_CHART_DIR} -f ${HELM_CHART_DIR}/values.yaml --namespace ${NAMESPACE} --create-namespace --atomic --install
}

processOptions () {
    while [[ $# > 0 ]]; do
        case "$1" in
            --dir)
                HELM_CHART_DIR=${2}; shift 2
            ;;   
            --release)
                HELM_RELEASE=${2}; shift 2
            ;; 
            --namespace)
                NAMESPACE=${2}; shift 2
            ;;                                 
        esac
    done
}

main () {
    echo -e "\nRunning"

    apply
}

############## Main

processOptions $*
main