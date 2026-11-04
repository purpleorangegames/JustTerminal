# hunt: marks wild animals to hunt, an edit mode command
# hunt 3, hunt boar for every boar, hunt off 3 takes the mark off

if args() = 0
  print dim usage: hunt [off] <number|kind>, wild lists them
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
for a in /wild
  if name(a) + 0 = w + 0 or lower(a/kind) = k or lower(a/kind) + "s" = k or lower(a/kind) + "es" = k or lower(a/name) = k
    try set a/hunt = on
    n = n + 1
  end
end
if n = 0
  print bad no wild {w}, wild lists them
  stop
end
print good {n} marked {on}
