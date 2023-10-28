<img src="res/logo.png" width="260"><br>

<img src="preview.png" width="500">

[Documentation](https://elk.strct.net)

## Installation

Installation steps:
* Install `.NET 8 SDK`
* Build the program `./build.sh`
* Install the program
  ```sh
  mkdir /usr/share/elk
  install -D ./build/* /usr/share/elk/
  ln -s /usr/share/elk/elk /usr/bin/elk
  ```
* Run the program `elk`
