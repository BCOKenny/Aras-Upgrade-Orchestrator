import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
const { SKILL_DIR, TMP_DIR, FINAL_PPTX } = process.env;
const { finalizePresentation } = await import(pathToFileURL(path.join(SKILL_DIR,"container_tools/artifact_tool_utils.mjs")).href);
const stage=path.join(TMP_DIR,"revision-finalizer");
await fs.mkdir(stage,{recursive:true});
const result=await finalizePresentation({
  explicitTotalSlideCount:14,
  requiredNativeTableOwnerSlides:[],
  workspaceDir:path.dirname(TMP_DIR),
  candidatePath:path.join(TMP_DIR,"stage-report-revision-draft.pptx"),
  finalPath:FINAL_PPTX,
  pythonExecutable:process.env.RUNTIME_PYTHON,
  integrityValidatorPath:path.join(SKILL_DIR,"container_tools/inspect_presentation_package_integrity.py"),
  layoutValidatorPath:path.join(SKILL_DIR,"container_tools/inspect_presentation_layout_geometry.py"),
  layoutArgs:["--expected-slide-size-emu","12192000,6858000","--validate-bullet-geometry","--validate-heading-fit"],
  fontPolicy:{basis:"design",families:["Microsoft JhengHei"]},
  verifyArtifactToolImport:true,
  receiptPath:path.join(stage,"revision.validation.json")
});
console.log(JSON.stringify(result));
