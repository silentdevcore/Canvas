# POSIX shell helper. Values stay inside the shell and are never logged.
pvad_environment_keys() {
    for pvad_key in "$@"; do
        case "$pvad_key" in PVAD_*) ;; *) return 1 ;; esac
        case "$pvad_key" in *[!A-Z0-9_]*) return 1 ;; esac
    done
}
