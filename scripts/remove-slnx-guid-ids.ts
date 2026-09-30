import { readdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const GUID_ID_PATTERN = /Id="\{?[0-9A-Fa-f]{8}-(?:[0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}\}?"/g;
const GUID_ID_WITH_WHITESPACE_PATTERN = /\s+Id="\{?[0-9A-Fa-f]{8}-(?:[0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}\}?"/g;

const EXCLUDED_DIRECTORIES = new Set([
	".git",
	"node_modules",
	"bin",
	"obj",
	"artifacts",
	"TestResults",
	".tools"
]);

const SCRIPT_DIRECTORY = path.dirname(fileURLToPath(import.meta.url));
const REPOSITORY_ROOT = path.resolve(SCRIPT_DIRECTORY, "..");

type Mode = "check" | "fix";

interface TextFile {
	content: string;
	hadBom: boolean;
}

function usage(): string {
	return [
		"Removes `Id=\"{GUID}\"` attributes from .slnx files.",
		"",
		"Usage:",
		"  bun scripts/remove-slnx-guid-ids.ts <check|fix>",
		"",
		"Commands:",
		"  check  Report every .slnx containing a GUID Id attribute (exit 1 if any found)",
		"  fix    Remove the attributes in place"
	].join("\n");
}

function parseMode(arg: string | undefined): Mode {
	if (arg === "check" || arg === "fix") {
		return arg;
	}

	console.log(usage());
	process.exit(arg === "--help" || arg === "-h" || arg === undefined ? 0 : 2);
}

async function findSolutionFiles(directory: string): Promise<string[]> {
	const results: string[] = [];
	const entries = await readdir(directory, { withFileTypes: true });
	entries.sort((a, b) => a.name.localeCompare(b.name));

	for (const entry of entries) {
		const fullPath = path.join(directory, entry.name);
		if (entry.isDirectory()) {
			if (!EXCLUDED_DIRECTORIES.has(entry.name)) {
				results.push(...(await findSolutionFiles(fullPath)));
			}
		} else if (entry.isFile() && entry.name.endsWith(".slnx")) {
			results.push(fullPath);
		}
	}

	return results;
}

async function readText(filePath: string): Promise<TextFile> {
	const raw = await readFile(filePath, "utf8");
	const hadBom = raw.charCodeAt(0) === 0xfeff;
	return { content: hadBom ? raw.slice(1) : raw, hadBom };
}

function lineNumberOf(content: string, index: number): number {
	let line = 1;
	for (let i = 0; i < index; i++) {
		if (content.charCodeAt(i) === 10) {
			line++;
		}
	}
	return line;
}

async function main(): Promise<void> {
	const mode = parseMode(process.argv[2]);
	const solutionFiles = await findSolutionFiles(REPOSITORY_ROOT);

	let violations = 0;

	for (const filePath of solutionFiles) {
		const { content, hadBom } = await readText(filePath);

		if (mode === "check") {
			for (const match of content.matchAll(GUID_ID_PATTERN)) {
				const line = lineNumberOf(content, match.index ?? 0);
				console.error(`${filePath}:${line}: found GUID Id attribute ${match[0]}`);
				violations++;
			}
		} else {
			const matches = content.match(GUID_ID_PATTERN);
			if (matches === null) {
				continue;
			}

			const updated = content.replace(GUID_ID_WITH_WHITESPACE_PATTERN, "");
			await writeFile(filePath, hadBom ? `\ufeff${updated}` : updated, "utf8");
			console.log(`${filePath}: removed ${matches.length} GUID Id attribute(s)`);
		}
	}

	if (mode === "check" && violations > 0) {
		process.exitCode = 1;
	}
}

await main();