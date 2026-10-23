# POSIX shell adapter. Values stay inside the shell and are never logged.
pvad_environment_alias() {
    if [ "$3" = x ] && [ "$5" = x ] && [ "$4" != "$6" ]; then
        printf 'Conflicting configuration keys: %s and %s.\n' "$1" "$2" >&2
        return 1
    fi
    if [ "$3" = x ]; then
        export "$1=$4"
    elif [ "$5" = x ]; then
        export "$1=$6"
    fi
    unset "$2"
}

pvad_environment_keys() {
    for pvad_key in "$@"; do
        case "$pvad_key" in PVAD_*) ;; *) return 1 ;; esac
        case "$pvad_key" in *[!A-Z0-9_]*) return 1 ;; esac
        pvad_legacy="PXA_${pvad_key#PVAD_}"
        # Only validated variable names enter eval; values are expanded by the shell, not evaluated.
        eval 'pvad_current_set=${'"$pvad_key"'+x}'
        eval 'pvad_current_value="${'"$pvad_key"'-}"'
        eval 'pvad_legacy_set=${'"$pvad_legacy"'+x}'
        eval 'pvad_legacy_value="${'"$pvad_legacy"'-}"'
        pvad_environment_alias "$pvad_key" "$pvad_legacy" "$pvad_current_set" "$pvad_current_value" \
            "$pvad_legacy_set" "$pvad_legacy_value" || return 1
    done
}
