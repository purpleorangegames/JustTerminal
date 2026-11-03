# slaughter: marks your animals to slaughter, an edit mode command
# slaughter 3, slaughter chicken for every chicken, slaughter off 3

if args() = 0
  print dim usage: slaughter [off] <number|name|kind>, cat /animals lists them
  stop
end
on = "yes"
w = arg(1)
if lower(w) = "off"
  on = "no"
  w = arg(2)
end
n = 0
for a in /animals
  if name(a) + 0 = w + 0 or lower(a/kind) = lower(replace(w, "_", " ")) or lower(a/name) = lower(replace(w, "_", " "))
    try set a/slaughter = on
    n = n + 1
  end
end
if n = 0
  print bad no animal {w}
  stop
end
print good {n} marked {on}
