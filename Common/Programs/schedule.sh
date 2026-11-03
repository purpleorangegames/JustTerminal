# schedule: show or set the hours, an edit mode command
# one letter an hour from midnight: s sleep, w work, j joy, a anything, m meditate
# schedule Tasca, schedule @night wwwwwwssssssssawwwwwjjjj

if args() = 0
  print dim usage: schedule <who> [24 letters]
  stop
end
out {pad("", 14)}
print dim 0     6     12    18
for p in pawns(arg(1))
  if args() > 1
    try set p/schedule = arg(2)
  end
  out bright {pad(p/name, 14)}
  print {p/schedule}
end
