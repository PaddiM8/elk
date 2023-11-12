# Path

The [env::path](/std/env::path/index) module used for working with the PATH
environment variable. When a path is added with `env::path::add`, it
is appended to `~/.config/elk/paths.txt`, which means it is going to
be added to the PATH variable automatically every new session.