# Installation

## Pre-built

Download the latest release ([Linux](https://github.com/PaddiM8/elk/releases/download/v0.0.0/linux-x64.tar.xz), [macOS](https://github.com/PaddiM8/elk/releases/download/v0.0.0/osx-x64.tar.xz))
and extract it into `/`:
```bash
tar xvf *.tar.xz -C /
```

## Manual Compilation

### Prerequisites

* A 64-bit Linux distro (it is possible to modify the `build.sh` file to 
compile for other platforms, but it may not work as expected)
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
mkdir -p /usr/share/elk
install -D ./build/* /usr/share/elk/
ln -s /usr/share/elk/elk /usr/bin/elk
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