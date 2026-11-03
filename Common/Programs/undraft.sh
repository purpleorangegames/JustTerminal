# undraft: let colonists go back to work, an edit mode command
# undraft all, undraft @guards

if args() = 0
  print dim usage: undraft <who>
  stop
end
for p in pawns(arg(1))
  try set p/drafted = no
end
print good back to work
