# slaughter: marks your animals to slaughter, an edit mode command
# slaughter 3, slaughter chicken for every chicken, slaughter off 3

if args() = 0
  print dim usage: slaughter [off] <number|name|kind>, cat /animals lists them
  stop
end
on = "yes"
first = 1
if lower(arg(1)) = "on"
  first = 2
end
if lower(arg(1)) = "off"
  on = "no"
  first = 2
end
# "hunt all rats" is the same as hunt rat
if lower(arg(first)) = "all" or lower(arg(first)) = "the"
  first = first + 1
end
w = rest(first)
k = lower(replace(w, "_", " "))
n = 0
for a in /animals
  if name(a) + 0 = w + 0 or lower(a/kind) = k or lower(a/kind) + "s" = k or lower(a/kind) + "es" = k or lower(a/name) = k
    try set a/slaughter = on
    n = n + 1
  end
end
if n = 0
  print bad no animal {w}
  stop
end
print good {n} marked {on}
