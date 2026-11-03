# asks first, then sets everyone to herbal medicine
# set only works in edit mode, type edit before running it

ask answer everyone to herbal medicine? y/n
if answer != "y"
  print dim left as it was
  stop
end
for p in /pawns
  try set p/medicine = "herbal"
end
print good everyone gets herbal medicine or worse
