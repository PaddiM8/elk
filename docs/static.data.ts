import { readFileSync } from "node:fs"

export const elkGrammar = {
  load() {
      return JSON.parse(readFileSync("../editors/vscode/syntaxes/elk.tmLanguage.json"))
  }
}

export const stdEntries = {
    load() {
        return JSON.parse(readFileSync("std/entries.json"))
    }
}
