# Installation

## Prerequisites

* A 64-bit Linux distro (it is possible to modify the `build.sh` file to 
compile for other platforms, but it may not work as expected)
* [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

## Steps

Start by cloning the repository:

```shell
git clone https://github.com/PaddiM8/elk
cd elk
```

Compile and install the program:

```shell
mkdir /usr/share/elk
install -D ./build/* /usr/share/elk/
ln -s /usr/share/elk/elk /usr/bin/elk
```

Elk is now installed and can be accessed in the terminal using the `elk` 
command.

### Default Shell

The first step is to add the path to the Elk binary to `/etc/shells` in order 
to make your system aware of the existence of the Elk shell.

After the path has been added to the `/etc/shells` file, Elk can be made the 
default shell by running the following command:

```shell
chsh -s /usr/bin/elk
```