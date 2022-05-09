autocmd BufNewFile,BufRead *.elk setfiletype elk

" Detect elk scripts by the shebang line.
autocmd BufNewFile,BufRead,StdinReadPost *
            \ if getline(1) =~ '^#!.*\Welk\s*$' |
            \     setfiletype elk |
            \ endif
