# tame: marks wild animals to tame, an edit mode command
# tame 3, tame boar for every boar, tame off 3 takes the mark off

if args() = 0
  print dim usage: tame [off] <number|kind>, wild lists them
  stop
end
on = "yes"
w = arg(1)
if lower(w) = "off"
  on = "no"
  w = arg(2)
end
n = 0
for a in /wild
  if name(a) + 0 = w + 0 or lower(a/kind) = lower(replace(w, "_", " ")) or lower(a/name) = lower(replace(w, "_", " "))
    try set a/tame = on
    n = n + 1
  end
end
if n = 0
  print bad no wild {w}, wild lists them
  stop
end
print good {n} marked {on}
