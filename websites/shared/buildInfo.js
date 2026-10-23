export const pvadVersion =
  typeof __PVAD_VERSION__ === 'undefined' ? 'development' : __PVAD_VERSION__;
export const pvadCommit =
  typeof __PVAD_BUILD_COMMIT__ === 'undefined' ? 'unknown' : __PVAD_BUILD_COMMIT__;
export const pvadBuildTime =
  typeof __PVAD_BUILD_TIME__ === 'undefined' ? null : __PVAD_BUILD_TIME__;
