# Installation

## Pre-built

Download the [latest release](https://github.com/PaddiM8/elk/releases/latest/) from GitHub.

### Linux/macOS
Extract the archive into `/`:
```bash
tar xvf *.tar.xz -C /
```

### Windows
Extract the zip file.

## Manual Compilation

### Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

### Steps

Start by cloning the repository:

```shell
git clone https://github.com/PaddiM8/elk
cd elk
```

Compile and install the program:

```shell
./build.sh
cd build

# Linux/macOS
tar -xvf */*.tar.xz -C /
```

Elk is now installed and can be accessed with the `elk` command.

### Default Shell

::: warning
Setting the default shell to a non-posix shell could lead to problems.
An alternative is to add `elk` at the end of your `.bashrc` or equivalent
file.
:::

When making Elk the default shell, first step is to add the path to the Elk binary to `/etc/shells`.
This makes your system aware of the existence of the shell.

After the path has been added to the `/etc/shells` file, Elk can be made the 
default shell by running the following command:

```shell
chsh -s /usr/bin/elk
```
