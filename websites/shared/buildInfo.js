export const pxaVersion =
  typeof __PXA_VERSION__ === 'undefined' ? 'development' : __PXA_VERSION__;
export const pxaCommit =
  typeof __PXA_BUILD_COMMIT__ === 'undefined' ? 'unknown' : __PXA_BUILD_COMMIT__;
export const pxaBuildTime =
  typeof __PXA_BUILD_TIME__ === 'undefined' ? null : __PXA_BUILD_TIME__;
