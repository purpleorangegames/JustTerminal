# allow: everything lying in a stockpile or on shelves, an edit mode command
# allow 3, allow all, allow a link or zone by its name with _ for spaces

if args() = 0
  print dim usage: allow <storage number|name|all>, storage lists them
  stop
end
w = lower(arg(1))
n = 0
for s in /storage
  if w = "all" or name(s) + 0 = w + 0 or w = replace(lower(s/name), " ", "_")
    set s/forbid = "no"
    n = n + 1
  end
end
if n = 0
  print bad no storage {arg(1)}, storage lists them
  stop
end
print good {n} done
