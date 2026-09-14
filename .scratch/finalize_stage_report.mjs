import path from "node:path";
import { pathToFileURL } from "node:url";
const { SKILL_DIR, TMP_DIR, FINAL_PPTX } = process.env;
const { finalizePresentation } = await import(pathToFileURL(path.join(SKILL_DIR, "container_tools/artifact_tool_utils.mjs")).href);
const stagingDir = path.join(TMP_DIR, "finalizer");
await (await import("node:fs/promises")).mkdir(stagingDir, { recursive: true });
const result = await finalizePresentation({
  explicitTotalSlideCount: 6,
  requiredNativeTableOwnerSlides: [],
  workspaceDir: path.dirname(TMP_DIR),
  candidatePath: path.join(TMP_DIR, "stage-report-draft.pptx"),
  finalPath: FINAL_PPTX,
  pythonExecutable: process.env.RUNTIME_PYTHON,
  integrityValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_package_integrity.py"),
  layoutValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_layout_geometry.py"),
  layoutArgs: ["--expected-slide-size-emu", "12192000,6858000", "--validate-bullet-geometry", "--validate-heading-fit"],
  fontPolicy: { basis: "design", families: ["Microsoft JhengHei"] },
  verifyArtifactToolImport: true,
  receiptPath: path.join(stagingDir, "stage-report.validation.json"),
});
console.log(JSON.stringify(result));
