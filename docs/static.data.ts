import { readFileSync } from "node:fs"

export const elkGrammar = {
  load() {
      return JSON.parse(readFileSync("../highlight/textmate/syntaxes/elk.tmLanguage.json"))
  }
}

export const stdEntries = {
    load() {
        return JSON.parse(readFileSync("std/entries.json"))
    }
}

export const plausibleSnippet = {
    async load() {
        const response = await fetch("https://plausible.kalker.xyz/js/script.js");

        return (await response.text()) as string;
    }
}