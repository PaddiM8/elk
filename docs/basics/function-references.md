# Function References

Function references are made using the `&` symbol. This can be done on both 
regular functions and programs. In order to call a function using a function 
reference, the `call` function is used.

<pre class="language-nim"><code class="lang-nim"><strong>let f = &#x26;len
</strong><strong>f | call abc #=> 3
</strong><strong>
</strong><strong>let output = if useEcho: &#x26;echo else &#x26;println
</strong>output | call hello world #=> hello world</code></pre>