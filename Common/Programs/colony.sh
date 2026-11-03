# colony: a summary of the colony

c = /colony

out dim {pad("colony", 12)}
print {c/name}
out dim {pad("storyteller", 12)}
print {c/storyteller}, {c/difficulty}
out dim {pad("day", 12)}
print {c/day}
out dim {pad("colonists", 12)}
print {c/colonists}
out dim {pad("prisoners", 12)}
print {c/prisoners}
out dim {pad("slaves", 12)}
print {c/slaves}
out dim {pad("animals", 12)}
print {c/animals}
out dim {pad("wealth", 12)}
print {num(c/wealth)}
out dim {pad("biome", 12)}
print {c/biome}
out dim {pad("maps", 12)}
print {c/maps}
