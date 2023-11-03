import { defineConfig } from "vitepress"
import { elkGrammar, stdEntries } from "../static.data"

const elkLanguage = {
  id: "elk",
  path: "",
  scopeName: "source.elk",
  grammar: elkGrammar.load(),
}

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: "elk",
  description: "A more programmatic shell language",
  head: [
    ["link", { rel: "icon", href: "/favicon.png" }],
    ["script", { "data-domain": "elk.strct.net", src: "/stats.js" }],
  ],
  markdown: {
    languages: [
        elkLanguage,
    ],
  },
  // https://vitepress.dev/reference/default-theme-config
  themeConfig: {
    logo: {
      light: "/favicon-light.png",
      dark: "/favicon.png",
    },
    search: {
      provider: "local",
    },

    nav: [
      { text: "Reference", link: "/" },
      { text: "Standard Library", link: "/std/" },
    ],

    sidebar: {
      "/": [
        {
          text: "Getting Started",
          items: [
            { text: "Installation", link: "/getting-started/installation" },
            { text: "Usage", link: "/getting-started/usage" },
            { text: "Examples", link: "/getting-started/examples" },
            { text: "Comparisons with Bash", link: "/getting-started/comparisons-with-bash" },
          ],
        },
        {
          text: "Basics",
          items: [
            { text: "Current Directory", link: "/basics/current-directory" },
            { text: "Fundamental Expressions", link: "/basics/fundamental-expressions" },
            { text: "Variables", link: "/basics/variables" },
            { text: "Functions & Structs", link: "/basics/functions-and-structs" },
            { text: "Program Invocation", link: "/basics/program-invocation" },
            { text: "Control Flow", link: "/basics/control-flow" },
            { text: "Data Types", link: "/basics/data-types" },
            { text: "Imports", link: "/basics/imports" },
            { text: "Error Handling", link: "/basics/error-handling" },
            { text: "Plurality", link: "/basics/plurality" },
            { text: "Function References", link: "/basics/function-references" },
            { text: "Closures", link: "/basics/closures" },
          ]
        },
        {
          text: "Other",
          items: [
            { text: "Bash Literals", link: "/other/bash-literals" },
            { text: "Casting", link: "/other/casting" },
            { text: "CLI Parsing", link: "/other/cli-parsing" },
            { text: "Shell Customisation", link: "/other/shell-customisation" },
            { text: "Pattern Matching", link: "/other/pattern-matching" },
            { text: "Conventions", link: "/other/conventions" },
            { text: "Multi-Line Expressions", link: "/other/multi-line-expressions" },
          ],
        },
        {
          items: [
            { text: "Standard Library", link: "/std/" },
          ],
        },
      ],
      "/std/": stdEntries.load(),
    },

    socialLinks: [
      { icon: "github", link: "https://github.com/PaddiM8/elk" },
    ],
  },
})
