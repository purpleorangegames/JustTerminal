# draft: draft colonists, an edit mode command
# draft Tasca, draft @guards, draft all

if args() = 0
  print dim usage: draft <who>
  stop
end
changed = ""
for p in pawns(arg(1))
  try set p/drafted = yes
  if error = none
    if changed != ""
      changed = changed + ", "
    end
    changed = changed + p/name
  end
end
if changed != ""
  print good {changed}: drafted
end
